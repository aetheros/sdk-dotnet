import { grey } from "@material-ui/core/colors";
import FloatingActionButton from "@material-ui/core/Fab";
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import Table from '@material-ui/core/Table';
import TableBody from '@material-ui/core/TableBody';
import TableCell from '@material-ui/core/TableCell';
import TableHead from '@material-ui/core/TableHead';
import TableRow from '@material-ui/core/TableRow';
import IconLink from "@material-ui/icons/Link";
import * as React from "react";

const styles = (theme: Theme) => createStyles({
	detailIcon: { fill: grey[500] },
});

interface Props extends WithStyles<typeof styles> {
	data: Meter[];
};

type Meter = {
	MeterId: string;
	MeterState: string;
};

class MeterListTableComponent extends React.Component<Props, any> {

	constructor(props) {
		super(props);
	}

	openDetail(meterId) {
		window.open(`/MeterDashboard/${meterId}`);
	};

	render() {
		const meters = this.props.data;

		return (
			<Table>
				<TableHead>
					<TableRow>
						<TableCell>ID</TableCell>
						<TableCell>State</TableCell>
						<TableCell>Detail</TableCell>
					</TableRow>
				</TableHead>
				<TableBody>
					{meters && meters.map(meter => (
						<TableRow key={meter.MeterId}>
							<TableCell>{meter.MeterId}</TableCell>
							<TableCell component="th" scope="row">
								{!meter ? "N/A" : meter.MeterState}
							</TableCell>
							<TableCell>
								<FloatingActionButton
									onClick={() => this.openDetail(meter.MeterId)}
								//zDepth={0}
								//size="small"
								//mini={true}
								//backgroundColor={grey[200]}
								//className={this.props.classes.floatingButton}
								//iconStyle={styles.detailIcon}
								>
									<IconLink className={this.props.classes.detailIcon} />
								</FloatingActionButton>
							</TableCell>
						</TableRow>
					))}
				</TableBody>
			</Table >
		);
	}
};
export default Object.assign(withStyles(styles)(MeterListTableComponent), { name: '' });