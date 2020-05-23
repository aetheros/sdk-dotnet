import MomentUtils from "@date-io/moment";
import TextField from '@material-ui/core/TextField';
import { DateTimePicker, MuiPickersUtilsProvider } from "@material-ui/pickers";
import * as moment from "moment";
import * as React from "react";
import AppDialog from "../meter/AppDialog";
import { MeterReadPolicy } from "./MeterConfig";

type Props = {
	open: boolean;
	onClose: React.ReactEventHandler;
	onSave: React.ReactEventHandler;
	onStartChange: React.ReactEventHandler;
	onEndChange: React.ReactEventHandler;
	onNameChange: React.ReactEventHandler;
	onPolicyIntervalChange: React.ReactEventHandler;
	meterReadPolicy: MeterReadPolicy;
	policyStart: string;
	policyEnd: string;
};

type State = {
	open: boolean;
	name: string;
};

export default class PolicyConfigDialog extends React.Component<Props, State> {

	constructor(props) {
		super(props);
		this.state = {
			open: props.open,
			name: null,
		};
	}

	handleNameChange(e) {
		this.props.onNameChange(e.target.value);
	}

	handleStartChange(e) {
		this.props.onStartChange(e._d);
	}

	handleEndChange(e) {
		this.props.onEndChange(e._d);
	}

	handlePolicyIntervalChange(e) {
		this.props.onPolicyIntervalChange(e.target.value);
	}

	dateFormat(date) {
		if (!date)
			return "";
		return moment(date).format("YYYY-MM-DD hh:mm A");
	}

	render() {
		const policyStart = this.props.policyStart;
		const meterReadPolicy = this.props.meterReadPolicy;
		const dateStart = policyStart ? policyStart : !meterReadPolicy ? Date.now() : meterReadPolicy.start !== null ? this.dateFormat(new Date(meterReadPolicy.start)) : this.dateFormat(Date.now());
		const policyEnd = this.props.policyEnd;
		const dateEnd = policyEnd ? policyEnd : !meterReadPolicy ? null : meterReadPolicy.end !== null ? this.dateFormat(new Date(meterReadPolicy.end)) : null;
		const defaultReadInterval = !meterReadPolicy ? "" : meterReadPolicy.readInterval;

		return (
			<MuiPickersUtilsProvider utils={MomentUtils}>
				<AppDialog
					open={this.props.open}
					onClose={this.props.onClose}
					onSave={this.props.onSave}
					title="Configure Usage Read Collection"
					content={
						<form>
							<div>
								<DateTimePicker
									value={dateStart}
									onChange={this.handleStartChange.bind(this)}
									format="YYYY-MM-DD hh:mm A"
									label="Start"
									showTodayButton
								/>
							</div>
							<div>
								<DateTimePicker
									value={dateEnd}
									onChange={this.handleEndChange.bind(this)}
									format="YYYY-MM-DD hh:mm A"
									label="End"
									showTodayButton
									disablePast
								/>
							</div>
							<div>
								<TextField
									id="readInterval"
									label="Meter Read Interval"
									defaultValue={defaultReadInterval}
									onChange={this.handlePolicyIntervalChange.bind(this)}
									margin="normal"
								/>
							</div>
						</form>
					} />
			</MuiPickersUtilsProvider>
		);
	}
};