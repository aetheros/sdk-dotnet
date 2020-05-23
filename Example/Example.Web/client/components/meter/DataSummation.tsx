import { List, ListItem, ListSubheader, Button, Icon, IconButton } from "@material-ui/core";
import { pink } from "@material-ui/core/colors";
import MenuItem from '@material-ui/core/MenuItem';
import Paper from "@material-ui/core/Paper";
import Select from '@material-ui/core/Select';
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import * as React from "react";
import { DataSummaryPoint, DataSummationChart } from "./DataSummationChart";
import { FormControl, InputLabel } from "@material-ui/core";


const styles = (theme: Theme) => createStyles({
	header: {
		color: "white",
		backgroundColor: pink[500],
		padding: 10,
		fontSize: 24,
	},
	selectList: {
		minWidth: 120,
	},
	selectLabel: { color: pink[400] },
	chart: {
		height: 400,
		width: "100%",
	},
	addButton: {
		marginLeft: 10,
	}
});

interface Props extends WithStyles<typeof styles> {
	data: DataSummaryPoint[];
	summationWindow: number;
	onWindowChange: (value: number) => void;
	onAddData: () => void;
	meterId: string;
};

type State = {
	summationWindow: number;
};

class DataSummationComponent extends React.Component<Props, State> {

	constructor(props: Props) {
		super(props);
		this.state = {
			summationWindow: props.summationWindow,
		};
	}

	handleChange(e: React.ChangeEvent<{ value: number }>) {
		this.props.onWindowChange(e.target.value);
	}

	render() {
		const summationWindows = [
			{
				Id: 1,
				Value: "1 Minute"
			},
			{
				Id: 1 * 60,
				Value: "1 Hour"
			},
			{
				Id: (1 * 24 * 60),
				Value: "24 hours"
			},
			{
				Id: (7 * 24 * 60),
				Value: "7 days"
			},
			{
				Id: (30 * 24 * 60),
				Value: "30 days"
			}
		];
		const classes = this.props.classes;

		return (
			<List dense={true} subheader={<ListSubheader>Water Use</ListSubheader>}>
				<ListItem>
					<form>
						<InputLabel id="summation-perdiod-label">Period</InputLabel>
						<Select
							labelId="summation-perdiod-label"
							className={classes.selectList}
							value={this.props.summationWindow || ""}
							autoWidth={false}
							onChange={this.handleChange.bind(this)}
						>
							<MenuItem value="">
								<em>None</em>
							</MenuItem>
							{summationWindows.map(item => <MenuItem key={item.Id} value={item.Id}>{item.Value}</MenuItem>)}
						</Select>

						<Button
							size="small"
							variant="contained"
							onClick={this.props.onAddData}
							endIcon={<Icon>add</Icon>}
							className={classes.addButton}
						>
							Add Data
						</Button>

					</form>
				</ListItem>
				<ListItem>
					<div className={classes.chart}>
						<DataSummationChart data={this.props.data} summationWindow={this.props.summationWindow} meterId={this.props.meterId} />
					</div>
				</ListItem>
			</List>
		);
	}
};

export default Object.assign(withStyles(styles)(DataSummationComponent), { name: '' });

