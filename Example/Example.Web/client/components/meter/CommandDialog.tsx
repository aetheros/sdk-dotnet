import MomentUtils from "@date-io/moment";
import FormControlLabel from '@material-ui/core/FormControlLabel';
import FormLabel from '@material-ui/core/FormLabel';
import Radio from '@material-ui/core/Radio';
import RadioGroup from '@material-ui/core/RadioGroup';
import { DateTimePicker, MuiPickersUtilsProvider } from "@material-ui/pickers";
import * as moment from "moment";
import * as React from "react";
import AppDialog from "../meter/AppDialog";

type Props = {
	open: boolean;
	onClose: any;
	onSave: any;
	valveState: string;
	onValveChange: any;
	onWhenChange: any;
	openValve: string;
	commandWhen: any;
};

type State = {
	date: Date;
	time: Date;
	open: boolean;
};

export default class CommandDialog extends React.Component<Props, State> {

	constructor(props) {
		super(props);

		this.state = {
			open: this.props.valveState === "Open" || this.props.openValve === "openValve"
		} as State;
	}

	handleValveChange(e) {
		console.log(`value: ${e.target.value} (${typeof (e.target.value)})`);
		this.setState({ open: e.target.value == "true" });
		this.props.onValveChange(e.target.value);
	};

	handleWhenChange(e) {
		this.props.onWhenChange(e._d);
	};

	formatDate(date) {
		if (!date)
			return "";
		return moment(date).format("YYYY-MM-DDTHH:mm");
	}

	onChangeDate = (date: Date) => {
		console.log('Date: ', date)
		this.setState({ date })
	}
	onChangeTime = (time: Date) => {
		console.log('Time: ', time)
		this.setState({ time })
	}

	render() {
		return (
			<AppDialog
				open={this.props.open}
				onClose={this.props.onClose}
				onSave={this.props.onSave}
				title="Water Main Valve Control"
				content={
					<form>
						<div>
							<FormLabel>Water Main Valve Shut Off</FormLabel>
							<RadioGroup
								aria-label="Water Main Valve Shut Off"
								name="valve"
								onChange={this.handleValveChange.bind(this)}
								value={this.state.open}
							>
								<FormControlLabel value={true} label="Open" control={<Radio />} />
								<FormControlLabel value={false} label="Close" control={<Radio />} />
							</RadioGroup>
						</div>
						<div>
							<MuiPickersUtilsProvider utils={MomentUtils}>
								<DateTimePicker
									value={this.props.commandWhen}
									disablePast
									onChange={this.handleWhenChange.bind(this)}
									format="YYYY-MM-DD hh:mm A"
									label="When"
									showTodayButton
								/>
							</MuiPickersUtilsProvider>
						</div>
					</form>
				} />
		);
	}
};